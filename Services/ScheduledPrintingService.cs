using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TaggJack.Models;
using TaggJack.Services.Inkjet;
using TaggJack.Services.Inkjet.Linx;

namespace TaggJack.Services;

public class ScheduledPrintingService
{
	private sealed class PrinterGateLease : IDisposable
	{
		private List<SemaphoreSlim>? _gates;

		public PrinterGateLease(List<SemaphoreSlim> gates)
		{
			_gates = gates;
		}

		public void Dispose()
		{
			List<SemaphoreSlim> list = Interlocked.Exchange(ref _gates, null);
			if (list != null)
			{
				for (int num = list.Count - 1; num >= 0; num--)
				{
					list[num].Release();
				}
			}
		}
	}

	private sealed class PreparedPrintGroup
	{
		public required string PrinterName { get; init; }

		public required string PrinterDisplayName { get; init; }

		public required DataTable Data { get; init; }
	}

	private sealed class PrintJobPreparation
	{
		public required int RecordCount { get; init; }

		public required List<PreparedPrintGroup> Groups { get; init; }

		public required StringBuilder DebugLog { get; init; }

		public required bool IsPerPrinterRouting { get; init; }
	}

	private Timer? _timer;

	private bool _isRunning;

	private int _activePrintJobs;

	private readonly object _lockObject = new object();

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _printerGates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

	private readonly ConcurrentDictionary<string, Task> _linxBackgroundJobs = new ConcurrentDictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

	private readonly ConcurrentDictionary<string, Task> _desktopBackgroundJobs = new ConcurrentDictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

	private readonly TemplateManagerService _templateManager;

	private readonly IPrintExecutor _printExecutor;

	private readonly int _intervalSeconds;

	private readonly List<ScheduledPrintLogEntry> _executionLogs;

	private CancellationTokenSource _serviceCancellation = new CancellationTokenSource();

	private const int MaxLogEntries = 50;

	public bool IsRunning => _isRunning;

	public bool IsSchedulerActive => _timer != null;

	public bool IsPrinting
	{
		get
		{
			lock (_lockObject)
			{
				return _activePrintJobs > 0;
			}
		}
	}

	public event EventHandler<string>? LogMessage;

	public event EventHandler<string>? ErrorOccurred;

	public event EventHandler<string>? PrintCompleted;

	public event EventHandler<bool>? PrintingStateChanged;

	public ScheduledPrintingService(TemplateManagerService templateManager, int intervalSeconds = 60, IPrintExecutor? printExecutor = null)
	{
		_templateManager = templateManager;
		_intervalSeconds = intervalSeconds;
		_printExecutor = printExecutor ?? new DesktopPrintExecutor();
		_executionLogs = new List<ScheduledPrintLogEntry>();
	}

	public void Start()
	{
		try
		{
			License.PortalLicenseService.Instance.RequireLicensed();
		}
		catch (License.LicenseDeniedException ex)
		{
			this.LogMessage?.Invoke(this, "Scheduled printing blocked: " + ex.Message);
			return;
		}
		lock (_lockObject)
		{
			if (_timer != null)
			{
				this.LogMessage?.Invoke(this, "Scheduled printing service is already running");
				return;
			}
			if (_serviceCancellation.IsCancellationRequested)
			{
				_serviceCancellation.Dispose();
				_serviceCancellation = new CancellationTokenSource();
			}
			_timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(_intervalSeconds));
			this.LogMessage?.Invoke(this, $"Scheduled printing service started (interval: {_intervalSeconds}s)");
		}
	}

	public void Stop()
	{
		lock (_lockObject)
		{
			_timer?.Dispose();
			_timer = null;
			_serviceCancellation.Cancel();
			this.LogMessage?.Invoke(this, "Scheduled printing service stopped");
		}
		// Linx/desktop background jobs observe _serviceCancellation and remove themselves from their job maps on completion.
	}

	public async Task RunNow()
	{
		await ProcessPrintCycle(CancellationToken.None);
	}

	private async void OnTimerTick(object? state)
	{
		await ProcessPrintCycle(CancellationToken.None);
	}

	private async Task ProcessPrintCycle(CancellationToken cancellationToken)
	{
		lock (_lockObject)
		{
			if (_isRunning)
			{
				this.LogMessage?.Invoke(this, "Print cycle already in progress, skipping this tick");
				return;
			}
			_isRunning = true;
		}
		CancellationToken linkedToken = cancellationToken;
		CancellationTokenSource linkedCancellation = null;
		try
		{
			linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_serviceCancellation.Token, cancellationToken);
			linkedToken = linkedCancellation.Token;
			if (linkedToken.IsCancellationRequested)
			{
				this.LogMessage?.Invoke(this, "Print cycle cancelled before start");
				return;
			}
			this.LogMessage?.Invoke(this, "Starting scheduled print cycle");
			List<LoadedTemplate> enabledTemplates = _templateManager.GetEnabledTemplates();
			if (enabledTemplates.Count == 0)
			{
				this.LogMessage?.Invoke(this, "No enabled templates to process");
				return;
			}
			this.LogMessage?.Invoke(this, $"Processing {enabledTemplates.Count} enabled template(s) in parallel");
			await Task.WhenAll(enabledTemplates.Select((LoadedTemplate template) => ProcessTemplate(template, linkedToken)).ToArray());
			this.LogMessage?.Invoke(this, "Print cycle completed");
		}
		catch (OperationCanceledException)
		{
			this.LogMessage?.Invoke(this, "Print cycle cancelled");
		}
		catch (Exception ex2)
		{
			this.ErrorOccurred?.Invoke(this, "Error in print cycle: " + ex2.Message);
		}
		finally
		{
			linkedCancellation?.Dispose();
			lock (_lockObject)
			{
				_isRunning = false;
			}
		}
	}

	private async Task ProcessTemplate(LoadedTemplate template, CancellationToken cancellationToken)
	{
		string logPrinter = template.Name;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			this.LogMessage?.Invoke(this, "Processing template: " + template.Name);
			RunNowDebugLog.WriteLine(logPrinter, "Processing template: " + template.Name + " from path: " + template.Path);
			if (!File.Exists(template.Path))
			{
				this.ErrorOccurred?.Invoke(this, "Template file not found: " + template.Path);
				RunNowDebugLog.WriteLine(logPrinter, "ERROR: Template file not found: " + template.Path);
				return;
			}
			LabelTemplate labelTemplate = await LoadTemplateFromFile(template.Path, cancellationToken);
			if (labelTemplate == null)
			{
				this.ErrorOccurred?.Invoke(this, "Failed to load template: " + template.Name);
				RunNowDebugLog.WriteLine(logPrinter, "ERROR: Failed to load template: " + template.Name);
				return;
			}
			logPrinter = ResolveLogPrinterName(labelTemplate);
			RunNowDebugLog.WriteLine(logPrinter, $"Template loaded: {labelTemplate.TemplateName}, Type={labelTemplate.TemplateType}, Elements: {labelTemplate.Elements.Count}, Size: {labelTemplate.LabelWidth}x{labelTemplate.LabelHeight}");
			if (labelTemplate.TemplateType.IsInkjetLike())
			{
				bool num = await ProcessInkjetTemplate(labelTemplate, template.Name, logPrinter, cancellationToken);
				_templateManager.UpdateLastPrintTime(template.Path);
				if (num)
				{
					this.PrintCompleted?.Invoke(this, "Successfully processed " + labelTemplate.TemplateType.DisplayName() + " template: " + template.Name);
				}
				return;
			}
			string printerName;
			if (labelTemplate.DataSource != null && !string.IsNullOrWhiteSpace(labelTemplate.DataSource.DefaultPrinter))
			{
				PrinterValidationResult printerValidationResult = PrinterService.ValidatePrinter(labelTemplate.DataSource.DefaultPrinter);
				printerName = printerValidationResult.SelectedPrinter;
				if (!string.IsNullOrWhiteSpace(printerValidationResult.Warning))
				{
					this.LogMessage?.Invoke(this, "Printer warning for " + template.Name + ": " + printerValidationResult.Warning);
				}
			}
			else
			{
				printerName = PrinterService.GetDefaultPrinter();
				this.LogMessage?.Invoke(this, "No printer specified for " + template.Name + ", using system default: " + printerName);
			}
			if (string.IsNullOrWhiteSpace(printerName))
			{
				this.ErrorOccurred?.Invoke(this, "No valid printer available for template: " + template.Name);
				return;
			}
			logPrinter = printerName;
			DataTable dataTable = null;
			if (labelTemplate.DataSource != null && !string.IsNullOrWhiteSpace(labelTemplate.DataSource.Query))
			{
				dataTable = await LoadTemplateData(labelTemplate, logPrinter, cancellationToken);
			}
			await SendPrintJob(labelTemplate, printerName, template.Name, dataTable, cancellationToken);
			_templateManager.UpdateLastPrintTime(template.Path);
			// PrintCompleted is raised by the desktop background job on real success (same as Linx).
		}
		catch (Exception ex)
		{
			this.ErrorOccurred?.Invoke(this, "Error processing template " + template.Name + ": " + ex.Message);
			RunNowDebugLog.WriteLine(logPrinter, "ERROR in ProcessTemplate: " + ex.Message);
		}
	}

	private async Task<bool> ProcessInkjetTemplate(LabelTemplate labelTemplate, string templateName, string logPrinter, CancellationToken cancellationToken)
	{
		TemplateType vendor = labelTemplate.TemplateType;
		string vendorName = vendor.DisplayName();
		InkjetConfig config = labelTemplate.InkjetConfig;
		if (config == null || string.IsNullOrWhiteSpace(config.PrinterHost) || string.IsNullOrWhiteSpace(config.JobName))
		{
			this.ErrorOccurred?.Invoke(this, vendorName + " template '" + templateName + "' is missing printer host or job/message name.");
			AddLog(templateName, 0, vendorName.ToLowerInvariant(), "Error: missing " + vendorName + " configuration");
			return false;
		}
		if (config.Mappings == null || config.Mappings.Count == 0)
		{
			this.ErrorOccurred?.Invoke(this, vendorName + " template '" + templateName + "' has no position mappings.");
			AddLog(templateName, 0, $"{config.PrinterHost}:{config.PrinterPort}", "Error: no position mappings");
			return false;
		}
		if (vendor.IsLinx())
		{
			foreach (InkjetPositionMapping mapping in config.Mappings)
			{
				if (string.IsNullOrWhiteSpace(mapping.PrinterFieldName))
				{
					this.ErrorOccurred?.Invoke(this, $"Linx template '{templateName}' mapping position {mapping.Position} is missing a printer field name.");
					AddLog(templateName, 0, $"{config.PrinterHost}:{config.PrinterPort}", $"Error: missing printer field name at position {mapping.Position}");
					return false;
				}
			}
		}
		DataTable dataTable = null;
		if (labelTemplate.DataSource != null && !string.IsNullOrWhiteSpace(labelTemplate.DataSource.Query))
		{
			dataTable = await LoadTemplateData(labelTemplate, logPrinter, cancellationToken);
		}
		string printerDisplay = $"{config.PrinterHost}:{config.PrinterPort}";
		if (dataTable == null || dataTable.Rows.Count == 0)
		{
			this.LogMessage?.Invoke(this, $"No records to print for {templateName}. Skipping {vendorName} job.");
			AddNoRecordsLog(templateName, printerDisplay);
			return true;
		}
		this.LogMessage?.Invoke(this, $"{vendorName} queue load: {dataTable.Rows.Count} record(s) for {templateName}");
		string printerKey = InkjetPrinterKey(config.PrinterHost, config.PrinterPort);

		// Linx buffer-drain can run for a long time; run it off the print cycle so other templates are not blocked.
		if (vendor.IsLinx())
		{
			return TryStartLinxBackgroundJob(labelTemplate, templateName, logPrinter, config, dataTable, printerKey, printerDisplay);
		}

		DateTime startedAt = DateTime.Now;
		InkjetOperationResult result;
		using (await AcquirePrinterGatesAsync(new string[1] { printerKey }, cancellationToken))
		{
			SetPrintingState(isPrinting: true);
			try
			{
				result = await new InkjetPrintService().QueueLoadAsync(config, dataTable, cancellationToken);
			}
			finally
			{
				SetPrintingState(isPrinting: false);
			}
		}
		string text2 = string.Empty;
		if (result.Success)
		{
			text2 = await MarkRecordsAsPrinted(labelTemplate, dataTable, logPrinter, cancellationToken);
		}
		string status = ((!result.Success) ? ("Error: " + result.Message) : (string.IsNullOrEmpty(text2) ? "Success" : "Success (Update errors)"));
		AddLog(templateName, dataTable.Rows.Count, printerDisplay, status, text2, result.ProtocolLog, startedAt);
		if (!result.Success)
		{
			this.ErrorOccurred?.Invoke(this, $"{vendorName} job failed for {templateName}: {result.Message}");
			return false;
		}
		this.LogMessage?.Invoke(this, vendorName + " job completed for " + templateName);
		return true;
	}

	/// <summary>
	/// Starts Linx queue-load on a background task. Always returns false so ProcessTemplate
	/// does not raise PrintCompleted early; the background job raises it on real success.
	/// </summary>
	private bool TryStartLinxBackgroundJob(LabelTemplate labelTemplate, string templateName, string logPrinter, InkjetConfig config, DataTable dataTable, string printerKey, string printerDisplay)
	{
		int recordCount = dataTable.Rows.Count;
		bool started;

		lock (_lockObject)
		{
			if (_linxBackgroundJobs.TryGetValue(printerKey, out Task? existing) && existing is { IsCompleted: false })
			{
				started = false;
			}
			else
			{
				CancellationToken cancellationToken = _serviceCancellation.Token;
				DateTime startedAt = DateTime.Now;
				Task job = Task.Run(async () =>
				{
					try
					{
						await RunLinxBackgroundJobAsync(labelTemplate, templateName, logPrinter, config, dataTable, printerKey, printerDisplay, cancellationToken, startedAt).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						this.LogMessage?.Invoke(this, $"Linx background job cancelled for {templateName}");
					}
					catch (Exception ex)
					{
						this.ErrorOccurred?.Invoke(this, $"Linx background job error for {templateName}: {ex.Message}");
						AddLog(templateName, recordCount, printerDisplay, "Error: " + ex.Message, startedAt: startedAt);
					}
				}, cancellationToken);
				_linxBackgroundJobs[printerKey] = job;
				_ = job.ContinueWith(t =>
				{
					_linxBackgroundJobs.TryRemove(new KeyValuePair<string, Task>(printerKey, t));
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				started = true;
			}
		}

		if (!started)
		{
			this.LogMessage?.Invoke(this, $"Linx job already in progress for {printerDisplay}; skipping {templateName} this cycle");
			AddLog(templateName, recordCount, printerDisplay, "Skipped: Linx job already in progress for this printer");
			return false;
		}

		this.LogMessage?.Invoke(this, $"Linx job started in background for {templateName} ({recordCount} record(s) → {printerDisplay})");
		return false;
	}

	private async Task RunLinxBackgroundJobAsync(LabelTemplate labelTemplate, string templateName, string logPrinter, InkjetConfig config, DataTable dataTable, string printerKey, string printerDisplay, CancellationToken cancellationToken, DateTime startedAt)
	{
		InkjetOperationResult result;
		using (await AcquirePrinterGatesAsync(new string[1] { printerKey }, cancellationToken).ConfigureAwait(false))
		{
			SetPrintingState(isPrinting: true);
			try
			{
				result = await new LinxPrintService().QueueLoadAsync(config, dataTable, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				SetPrintingState(isPrinting: false);
			}
		}

		string updateErrors = string.Empty;
		if (result.Success)
		{
			updateErrors = await MarkRecordsAsPrinted(labelTemplate, dataTable, logPrinter, cancellationToken).ConfigureAwait(false);
		}

		string status = ((!result.Success) ? ("Error: " + result.Message) : (string.IsNullOrEmpty(updateErrors) ? "Success" : "Success (Update errors)"));
		AddLog(templateName, dataTable.Rows.Count, printerDisplay, status, updateErrors, result.ProtocolLog, startedAt);
		if (!result.Success)
		{
			this.ErrorOccurred?.Invoke(this, $"Linx job failed for {templateName}: {result.Message}");
			return;
		}

		this.LogMessage?.Invoke(this, "Linx job completed for " + templateName);
		this.PrintCompleted?.Invoke(this, "Successfully processed Linx template: " + templateName);
	}

	private async Task<LabelTemplate?> LoadTemplateFromFile(string filePath, CancellationToken cancellationToken)
	{
		try
		{
			LabelTemplate labelTemplate = await Task.Run(delegate
			{
				cancellationToken.ThrowIfCancellationRequested();
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(LabelTemplate));
				using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				return (LabelTemplate)xmlSerializer.Deserialize(stream);
			}, cancellationToken);
			if (labelTemplate != null)
			{
				this.LogMessage?.Invoke(this, $"Template loaded with {labelTemplate.Elements.Count} elements");
			}
			return await Task.FromResult(labelTemplate);
		}
		catch (Exception ex)
		{
			this.ErrorOccurred?.Invoke(this, "Error loading template from " + filePath + ": " + ex.Message);
			return null;
		}
	}

	private async Task<DataTable?> LoadTemplateData(LabelTemplate template, string logPrinter, CancellationToken cancellationToken)
	{
		try
		{
			if (template.DataSource == null || string.IsNullOrWhiteSpace(template.DataSource.Query))
			{
				return null;
			}
			this.LogMessage?.Invoke(this, "Loading data for template: " + template.TemplateName);
			RunNowDebugLog.WriteLine(logPrinter, "Executing query: " + template.DataSource.Query);
			MySqlService mysqlService = new MySqlService
			{
				ConnectionString = $"Server={template.DataSource.Host};Port={template.DataSource.Port};Database={template.DataSource.Database};Uid={template.DataSource.Username};Pwd={template.DataSource.Password};SslMode=None;AllowPublicKeyRetrieval=True;"
			};
			DataTable dataTable = await Task.Run(delegate
			{
				cancellationToken.ThrowIfCancellationRequested();
				return mysqlService.ExecuteQuery(template.DataSource.Query);
			}, cancellationToken);
			RunNowDebugLog.WriteLine(logPrinter, $"Query returned {dataTable?.Rows.Count ?? 0} records");
			this.LogMessage?.Invoke(this, $"Loaded {dataTable?.Rows.Count ?? 0} records for template: {template.TemplateName}");
			if (dataTable != null && dataTable.Rows.Count > 0)
			{
				RunNowDebugLog.WriteLine(logPrinter, "Columns: " + string.Join(", ", from DataColumn c in dataTable.Columns
					select c.ColumnName));
				for (int num = 0; num < Math.Min(3, dataTable.Rows.Count); num++)
				{
					DataRow dataRow = dataTable.Rows[num];
					string value = string.Join(", ", dataRow.ItemArray.Select((object v) => v?.ToString() ?? "NULL"));
					RunNowDebugLog.WriteLine(logPrinter, $"  Row {num}: {value}");
				}
			}
			return dataTable;
		}
		catch (Exception ex)
		{
			this.ErrorOccurred?.Invoke(this, "Error loading data for template " + template.TemplateName + ": " + ex.Message);
			RunNowDebugLog.WriteLine(logPrinter, "ERROR loading data: " + ex.Message);
			return null;
		}
	}

	private async Task SendPrintJob(LabelTemplate template, string printerName, string templateName, DataTable? dataTable, CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (dataTable == null || dataTable.Rows.Count == 0)
			{
				this.LogMessage?.Invoke(this, "No records to print for " + templateName + ". Skipping print job.");
				RunNowDebugLog.WriteLine(printerName, "No records found - skipping print job for " + templateName);
				AddNoRecordsLog(templateName, printerName);
				return;
			}
			PrintJobPreparation preparedJob = await Task.Run(() => PreparePrintJob(template, printerName, templateName, dataTable, cancellationToken), cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			AppendDebugLog(printerName, preparedJob.DebugLog.ToString());
			if (preparedJob.Groups.Count == 0)
			{
				this.LogMessage?.Invoke(this, "No printer groups to print for " + templateName);
				RunNowDebugLog.WriteLine(printerName, "No printer groups prepared for " + templateName);
				return;
			}
			DateTime startedAt = DateTime.Now;
			int started = 0;
			foreach (PreparedPrintGroup group in preparedJob.Groups)
			{
				if (TryStartDesktopBackgroundJob(template, templateName, group, startedAt))
				{
					started++;
				}
			}
			if (started > 0)
			{
				this.LogMessage?.Invoke(this, $"Dispatched {started} desktop print job(s) in background for {templateName} ({preparedJob.RecordCount} record(s))");
				RunNowDebugLog.WriteLine(printerName, $"Dispatched {started}/{preparedJob.Groups.Count} desktop print job(s) in background for {templateName}");
			}
		}
		catch (OperationCanceledException)
		{
			this.LogMessage?.Invoke(this, "Print job cancelled for " + templateName);
		}
		catch (Exception ex2)
		{
			this.ErrorOccurred?.Invoke(this, "Error sending print job for " + templateName + ": " + ex2.Message);
			RunNowDebugLog.WriteLine(printerName, "ERROR sending print job: " + ex2.Message);
			AddLog(templateName, 0, printerName, "Error: " + ex2.Message);
		}
	}

	/// <summary>
	/// Starts a desktop print on a background task keyed by printer. Returns false if that printer
	/// already has an in-flight job so the schedule cycle can continue for other printers.
	/// </summary>
	private bool TryStartDesktopBackgroundJob(LabelTemplate template, string templateName, PreparedPrintGroup group, DateTime startedAt)
	{
		string printerKey = DesktopPrinterKey(group.PrinterName);
		int recordCount = group.Data.Rows.Count;
		bool started;

		lock (_lockObject)
		{
			if (_desktopBackgroundJobs.TryGetValue(printerKey, out Task? existing) && existing is { IsCompleted: false })
			{
				started = false;
			}
			else
			{
				CancellationToken cancellationToken = _serviceCancellation.Token;
				Task job = Task.Run(async () =>
				{
					try
					{
						await RunDesktopBackgroundJobAsync(template, templateName, group, cancellationToken, startedAt).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						this.LogMessage?.Invoke(this, $"Desktop background job cancelled for {templateName} → {group.PrinterName}");
					}
					catch (Exception ex)
					{
						this.ErrorOccurred?.Invoke(this, $"Desktop background job error for {templateName}: {ex.Message}");
						AddLog(templateName, recordCount, group.PrinterDisplayName, "Error: " + ex.Message, startedAt: startedAt);
					}
				}, cancellationToken);
				_desktopBackgroundJobs[printerKey] = job;
				_ = job.ContinueWith(t =>
				{
					_desktopBackgroundJobs.TryRemove(new KeyValuePair<string, Task>(printerKey, t));
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				started = true;
			}
		}

		if (!started)
		{
			this.LogMessage?.Invoke(this, $"Desktop print already in progress for {group.PrinterDisplayName}; skipping {templateName} this cycle");
			RunNowDebugLog.WriteLine(group.PrinterName, $"Skipped {templateName}: print already in progress for this printer");
			AddLog(templateName, recordCount, group.PrinterDisplayName, "Skipped: print already in progress for this printer");
			return false;
		}

		this.LogMessage?.Invoke(this, $"Desktop job started in background for {templateName} ({recordCount} record(s) → {group.PrinterDisplayName})");
		return true;
	}

	private async Task RunDesktopBackgroundJobAsync(LabelTemplate template, string templateName, PreparedPrintGroup group, CancellationToken cancellationToken, DateTime startedAt)
	{
		string gateKey = DesktopPrinterKey(group.PrinterName);
		using (await AcquirePrinterGatesAsync(new string[1] { gateKey }, cancellationToken).ConfigureAwait(false))
		{
			SetPrintingState(isPrinting: true);
			try
			{
				this.LogMessage?.Invoke(this, $"Sending {group.Data.Rows.Count} label(s) to {group.PrinterName}");
				await _printExecutor.PrintAllRecordsAsync(template, group.PrinterName, group.Data, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				SetPrintingState(isPrinting: false);
			}
		}

		string updateErrors = await MarkRecordsAsPrinted(template, group.Data, group.PrinterName, cancellationToken).ConfigureAwait(false);
		string status = (string.IsNullOrEmpty(updateErrors) ? "Success" : "Success (Update errors)");
		AddLog(templateName, group.Data.Rows.Count, group.PrinterDisplayName, status, updateErrors, startedAt: startedAt);
		this.LogMessage?.Invoke(this, "Desktop print job completed for " + templateName + " → " + group.PrinterDisplayName);
		RunNowDebugLog.WriteLine(group.PrinterName, "Print job completed for " + templateName);
		this.PrintCompleted?.Invoke(this, "Successfully printed template: " + templateName);
	}

	private PrintJobPreparation PreparePrintJob(LabelTemplate template, string defaultPrinter, string templateName, DataTable dataTable, CancellationToken cancellationToken)
	{
		PrintJobPreparation printJobPreparation = new PrintJobPreparation
		{
			RecordCount = dataTable.Rows.Count,
			Groups = new List<PreparedPrintGroup>(),
			DebugLog = new StringBuilder(),
			IsPerPrinterRouting = dataTable.Columns.Contains("printer")
		};
		AppendLogLine(printJobPreparation.DebugLog, "Sending print job for '" + templateName + "' to printer: " + defaultPrinter);
		AppendLogLine(printJobPreparation.DebugLog, $"Template details: Name='{template.TemplateName}', Elements={template.Elements.Count}");
		for (int i = 0; i < Math.Min(5, template.Elements.Count); i++)
		{
			LabelElement labelElement = template.Elements[i];
			AppendLogLine(printJobPreparation.DebugLog, $"Element {i}: Type={labelElement.Type}, Content='{labelElement.Content?.Substring(0, Math.Min(50, labelElement.Content?.Length ?? 0))}'");
		}
		AppendLogLine(printJobPreparation.DebugLog, $"Will print {printJobPreparation.RecordCount} label(s)");
		AppendLogLine(printJobPreparation.DebugLog, $"DataTable has {dataTable.Columns.Count} columns, {dataTable.Rows.Count} rows");
		if (!printJobPreparation.IsPerPrinterRouting)
		{
			printJobPreparation.Groups.Add(new PreparedPrintGroup
			{
				PrinterName = defaultPrinter,
				PrinterDisplayName = defaultPrinter,
				Data = dataTable
			});
			AppendLogLine(printJobPreparation.DebugLog, "No 'printer' column - using default printer for all records");
			return printJobPreparation;
		}
		AppendLogLine(printJobPreparation.DebugLog, "'printer' column found - grouping records by printer");
		Dictionary<string, DataTable> dictionary = new Dictionary<string, DataTable>();
		Dictionary<string, HashSet<string>> dictionary2 = new Dictionary<string, HashSet<string>>();
		foreach (DataRow row in dataTable.Rows)
		{
			cancellationToken.ThrowIfCancellationRequested();
			object obj = row["printer"];
			string text = (string.IsNullOrWhiteSpace(obj?.ToString()) ? defaultPrinter : obj.ToString().Trim());
			string text2 = text;
			PrinterValidationResult printerValidationResult = PrinterService.ValidatePrinter(text);
			if (!printerValidationResult.IsValid)
			{
				string text3 = ((row.ItemArray.Length == 0) ? "unknown" : row.ItemArray[0]?.ToString());
				this.ErrorOccurred?.Invoke(this, "Skipping record " + text3 + ": " + printerValidationResult.Warning);
				AppendLogLine(printJobPreparation.DebugLog, "SKIPPED record " + text3 + ": " + printerValidationResult.Warning);
				continue;
			}
			if (!string.IsNullOrWhiteSpace(printerValidationResult.Warning))
			{
				string text4 = ((row.ItemArray.Length == 0) ? "unknown" : row.ItemArray[0]?.ToString());
				AppendLogLine(printJobPreparation.DebugLog, "Record " + text4 + ": " + printerValidationResult.Warning);
			}
			text = printerValidationResult.SelectedPrinter;
			if (text != text2)
			{
				if (!dictionary2.ContainsKey(text))
				{
					dictionary2[text] = new HashSet<string>();
				}
				dictionary2[text].Add(text2);
			}
			if (!dictionary.ContainsKey(text))
			{
				dictionary[text] = dataTable.Clone();
			}
			dictionary[text].ImportRow(row);
		}
		AppendLogLine(printJobPreparation.DebugLog, $"Grouped into {dictionary.Count} printer group(s)");
		foreach (KeyValuePair<string, DataTable> item in dictionary)
		{
			string key = item.Key;
			DataTable value = item.Value;
			string printerDisplayName = key;
			if (dictionary2.ContainsKey(key))
			{
				string text5 = string.Join(", ", dictionary2[key]);
				printerDisplayName = key + " (" + text5 + " not found)";
			}
			AppendLogLine(printJobPreparation.DebugLog, $"Prepared {value.Rows.Count} record(s) for printer: {key}");
			printJobPreparation.Groups.Add(new PreparedPrintGroup
			{
				PrinterName = key,
				PrinterDisplayName = printerDisplayName,
				Data = value
			});
		}
		return printJobPreparation;
	}

	public void Dispose()
	{
		Stop();
	}

	private void AddLog(string templateName, int recordCount, string printerName, string status = "Success", string updateErrors = "", string protocolLog = "", DateTime? startedAt = null)
	{
		lock (_lockObject)
		{
			ScheduledPrintLogEntry item = new ScheduledPrintLogEntry
			{
				ExecutionTime = startedAt ?? DateTime.Now,
				TemplateName = templateName,
				RecordCount = recordCount,
				PrinterName = printerName,
				Status = status,
				UpdateErrors = updateErrors,
				ProtocolLog = (protocolLog ?? string.Empty)
			};
			_executionLogs.Insert(0, item);
			if (_executionLogs.Count > 50)
			{
				_executionLogs.RemoveRange(50, _executionLogs.Count - 50);
			}
		}
	}

	private void AddNoRecordsLog(string templateName, string printerName)
	{
		lock (_lockObject)
		{
			ScheduledPrintLogEntry scheduledPrintLogEntry = _executionLogs.FirstOrDefault((ScheduledPrintLogEntry e) => string.Equals(e.TemplateName, templateName, StringComparison.OrdinalIgnoreCase));
			if (scheduledPrintLogEntry != null && scheduledPrintLogEntry.Status == "No records to print" && scheduledPrintLogEntry.RecordCount == 0)
			{
				scheduledPrintLogEntry.ExecutionTime = DateTime.Now;
				scheduledPrintLogEntry.PrinterName = printerName;
				scheduledPrintLogEntry.NoRecordsCheckCount++;
				RunNowDebugLog.WriteLine(printerName, $"Updated timestamp for existing 'No records' log entry (count: {scheduledPrintLogEntry.NoRecordsCheckCount})");
				return;
			}
			ScheduledPrintLogEntry item = new ScheduledPrintLogEntry
			{
				ExecutionTime = DateTime.Now,
				TemplateName = templateName,
				RecordCount = 0,
				PrinterName = printerName,
				Status = "No records to print",
				UpdateErrors = string.Empty,
				NoRecordsCheckCount = 1
			};
			_executionLogs.Insert(0, item);
			if (_executionLogs.Count > 50)
			{
				_executionLogs.RemoveRange(50, _executionLogs.Count - 50);
			}
		}
	}

	public List<ScheduledPrintLogEntry> GetLogs()
	{
		lock (_lockObject)
		{
			return new List<ScheduledPrintLogEntry>(_executionLogs);
		}
	}

	private async Task<string> MarkRecordsAsPrinted(LabelTemplate template, DataTable dataTable, string logPrinter, CancellationToken cancellationToken)
	{
		try
		{
			if (template.DataSource == null || string.IsNullOrWhiteSpace(template.DataSource.UpdateQuery))
			{
				return string.Empty;
			}
			string updateQuery = template.DataSource.UpdateQuery;
			this.LogMessage?.Invoke(this, $"Marking {dataTable.Rows.Count} record(s) as printed");
			RunNowDebugLog.WriteLine(logPrinter, $"Executing update query for {dataTable.Rows.Count} records");
			MySqlService mysqlService = new MySqlService
			{
				ConnectionString = $"Server={template.DataSource.Host};Port={template.DataSource.Port};Database={template.DataSource.Database};Uid={template.DataSource.Username};Pwd={template.DataSource.Password};SslMode=None;AllowPublicKeyRetrieval=True;"
			};
			int successCount = 0;
			int failureCount = 0;
			StringBuilder errorMessages = new StringBuilder();
			StringBuilder debugBuilder = new StringBuilder();
			int loggedSqlCount = 0;
			foreach (DataRow row in dataTable.Rows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					string query = ReplacePlaceholders(updateQuery, row, dataTable);
					if (loggedSqlCount < 5)
					{
						AppendLogLine(debugBuilder, "Executing SQL: " + query);
						loggedSqlCount++;
					}
					await Task.Run(() => mysqlService.ExecuteNonQuery(query), cancellationToken);
					successCount++;
				}
				catch (Exception ex)
				{
					failureCount++;
					string text = ((row.ItemArray.Length == 0) ? "unknown" : row.ItemArray[0]?.ToString());
					string value = "Record " + text + ": " + ex.Message;
					if (errorMessages.Length == 0)
					{
						errorMessages.AppendLine(value);
					}
					this.ErrorOccurred?.Invoke(this, "Failed to mark record " + text + " as printed: " + ex.Message);
					AppendLogLine(debugBuilder, "ERROR marking record " + text + ": " + ex.Message);
				}
			}
			this.LogMessage?.Invoke(this, $"Update complete: {successCount} success, {failureCount} failed");
			AppendLogLine(debugBuilder, $"Update complete: {successCount} success, {failureCount} failed");
			if (dataTable.Rows.Count > 5)
			{
				AppendLogLine(debugBuilder, $"Skipped logging SQL for {dataTable.Rows.Count - 5} additional record(s)");
			}
			AppendDebugLog(logPrinter, debugBuilder.ToString());
			return (failureCount > 0) ? $"{failureCount} update(s) failed. Example error:\n{errorMessages}" : string.Empty;
		}
		catch (OperationCanceledException)
		{
			this.LogMessage?.Invoke(this, "Record update cancelled");
			return "Update operation cancelled.";
		}
		catch (Exception ex3)
		{
			this.ErrorOccurred?.Invoke(this, "Error executing update query: " + ex3.Message);
			RunNowDebugLog.WriteLine(logPrinter, "ERROR in MarkRecordsAsPrinted: " + ex3.Message);
			return "Update query failed: " + ex3.Message;
		}
	}

	private string ReplacePlaceholders(string query, DataRow row, DataTable dataTable)
	{
		string text = query;
		foreach (Match item in new Regex("\\{([^}]+)\\}").Matches(query))
		{
			string value = item.Groups[1].Value;
			if (dataTable.Columns.Contains(value))
			{
				string newValue = row[value]?.ToString() ?? "";
				text = text.Replace(item.Value, newValue);
			}
			else
			{
				this.LogMessage?.Invoke(this, "Warning: Field '" + value + "' not found in query results");
			}
		}
		return text;
	}

	private static void AppendLogLine(StringBuilder builder, string message)
	{
		builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
			.AppendLine(message);
	}

	private static void AppendDebugLog(string printerName, string message)
	{
		RunNowDebugLog.Append(printerName, message);
	}

	private static string ResolveLogPrinterName(LabelTemplate template)
	{
		if (template.TemplateType.IsInkjetLike() && template.InkjetConfig != null && !string.IsNullOrWhiteSpace(template.InkjetConfig.PrinterHost))
		{
			return $"{template.InkjetConfig.PrinterHost}:{template.InkjetConfig.PrinterPort}";
		}
		if (template.DataSource != null && !string.IsNullOrWhiteSpace(template.DataSource.DefaultPrinter))
		{
			return template.DataSource.DefaultPrinter;
		}
		if (!string.IsNullOrWhiteSpace(template.TemplateName))
		{
			return template.TemplateName;
		}
		return "unknown";
	}

	private void SetPrintingState(bool isPrinting)
	{
		bool? flag = null;
		lock (_lockObject)
		{
			if (isPrinting)
			{
				_activePrintJobs++;
				if (_activePrintJobs == 1)
				{
					flag = true;
				}
			}
			else if (_activePrintJobs > 0)
			{
				_activePrintJobs--;
				if (_activePrintJobs == 0)
				{
					flag = false;
				}
			}
		}
		if (flag.HasValue)
		{
			this.PrintingStateChanged?.Invoke(this, flag.Value);
		}
	}

	private static string InkjetPrinterKey(string host, int port)
	{
		return $"inkjet:{host.Trim().ToLowerInvariant()}:{port}";
	}

	private static string DesktopPrinterKey(string printerName)
	{
		return "desktop:" + printerName.Trim();
	}

	private SemaphoreSlim GetPrinterGate(string key)
	{
		return _printerGates.GetOrAdd(key, (string _) => new SemaphoreSlim(1, 1));
	}

	private async Task<IDisposable> AcquirePrinterGatesAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
	{
		List<string> list = keys.Where((string k) => !string.IsNullOrWhiteSpace(k)).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string k) => k, StringComparer.OrdinalIgnoreCase)
			.ToList();
		List<SemaphoreSlim> acquired = new List<SemaphoreSlim>(list.Count);
		try
		{
			foreach (string item in list)
			{
				SemaphoreSlim gate = GetPrinterGate(item);
				await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				acquired.Add(gate);
			}
			return new PrinterGateLease(acquired);
		}
		catch
		{
			for (int num = acquired.Count - 1; num >= 0; num--)
			{
				acquired[num].Release();
			}
			throw;
		}
	}
}
