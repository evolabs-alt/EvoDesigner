using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using EvoDesigner.Models;
using EvoDesigner.Services;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace EvoDesigner.Services
{
    public class ScheduledPrintingService
    {
        private System.Threading.Timer? _timer;
        private bool _isRunning = false;
        private readonly object _lockObject = new object();
        private readonly TemplateManagerService _templateManager;
        private readonly int _intervalSeconds;
        private readonly List<ScheduledPrintLogEntry> _executionLogs;
        private const int MaxLogEntries = 50;

        public event EventHandler<string>? LogMessage;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<string>? PrintCompleted;

        public bool IsRunning => _isRunning;
        public bool IsSchedulerActive => _timer != null;

        public ScheduledPrintingService(TemplateManagerService templateManager, int intervalSeconds = 60)
        {
            _templateManager = templateManager;
            _intervalSeconds = intervalSeconds;
            _executionLogs = new List<ScheduledPrintLogEntry>();
        }

        /// <summary>
        /// Starts the scheduled printing service
        /// </summary>
        public void Start()
        {
            lock (_lockObject)
            {
                if (_timer != null)
                {
                    LogMessage?.Invoke(this, "Scheduled printing service is already running");
                    return;
                }

                _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(_intervalSeconds));
                LogMessage?.Invoke(this, $"Scheduled printing service started (interval: {_intervalSeconds}s)");
            }
        }

        /// <summary>
        /// Stops the scheduled printing service
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                _timer?.Dispose();
                _timer = null;
                LogMessage?.Invoke(this, "Scheduled printing service stopped");
            }
        }

        /// <summary>
        /// Manually triggers a print cycle
        /// </summary>
        public async Task RunNow()
        {
            await ProcessPrintCycle();
        }

        private async void OnTimerTick(object? state)
        {
            await ProcessPrintCycle();
        }

        private async Task ProcessPrintCycle()
        {
            // Reentrancy protection
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    LogMessage?.Invoke(this, "Print cycle already in progress, skipping this tick");
                    return;
                }
                _isRunning = true;
            }

            try
            {
                LogMessage?.Invoke(this, "Starting scheduled print cycle");

                var enabledTemplates = _templateManager.GetEnabledTemplates();
                
                if (enabledTemplates.Count == 0)
                {
                    LogMessage?.Invoke(this, "No enabled templates to process");
                    return;
                }

                LogMessage?.Invoke(this, $"Processing {enabledTemplates.Count} enabled template(s)");

                foreach (var template in enabledTemplates)
                {
                    await ProcessTemplate(template);
                }

                LogMessage?.Invoke(this, "Print cycle completed");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error in print cycle: {ex.Message}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isRunning = false;
                }
            }
        }

        private async Task ProcessTemplate(LoadedTemplate template)
        {
            try
            {
                LogMessage?.Invoke(this, $"Processing template: {template.Name}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Processing template: {template.Name} from path: {template.Path}" + Environment.NewLine);

                // Validate template file exists
                if (!File.Exists(template.Path))
                {
                    ErrorOccurred?.Invoke(this, $"Template file not found: {template.Path}");
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Template file not found: {template.Path}" + Environment.NewLine);
                    return;
                }

                // Load and parse template
                var labelTemplate = await LoadTemplateFromFile(template.Path);
                if (labelTemplate == null)
                {
                    ErrorOccurred?.Invoke(this, $"Failed to load template: {template.Name}");
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Failed to load template: {template.Name}" + Environment.NewLine);
                    return;
                }
                
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Template loaded: {labelTemplate.TemplateName}, Elements: {labelTemplate.Elements.Count}, Size: {labelTemplate.LabelWidth}x{labelTemplate.LabelHeight}" + Environment.NewLine);

                // Validate printer
                string printerName = "";
                if (labelTemplate.DataSource != null && !string.IsNullOrWhiteSpace(labelTemplate.DataSource.DefaultPrinter))
                {
                    var printerValidation = PrinterService.ValidatePrinter(labelTemplate.DataSource.DefaultPrinter);
                    printerName = printerValidation.SelectedPrinter;
                    
                    if (!string.IsNullOrWhiteSpace(printerValidation.Warning))
                    {
                        LogMessage?.Invoke(this, $"Printer warning for {template.Name}: {printerValidation.Warning}");
                    }
                }
                else
                {
                    printerName = PrinterService.GetDefaultPrinter();
                    LogMessage?.Invoke(this, $"No printer specified for {template.Name}, using system default: {printerName}");
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    ErrorOccurred?.Invoke(this, $"No valid printer available for template: {template.Name}");
                    return;
                }

                // Load data if data source is configured
                DataTable? dataTable = null;
                if (labelTemplate.DataSource != null && !string.IsNullOrWhiteSpace(labelTemplate.DataSource.Query))
                {
                    dataTable = await LoadTemplateData(labelTemplate);
                }

                // Send print job (with data if available)
                await SendPrintJob(labelTemplate, printerName, template.Name, dataTable);

                // Update last print time
                _templateManager.UpdateLastPrintTime(template.Path);

                PrintCompleted?.Invoke(this, $"Successfully printed template: {template.Name}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing template {template.Name}: {ex.Message}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in ProcessTemplate: {ex.Message}" + Environment.NewLine);
            }
        }

        private async Task<LabelTemplate?> LoadTemplateFromFile(string filePath)
        {
            try
            {
                // Use XML deserialization to load the full template with all elements
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LabelTemplate));
                using var fs = new FileStream(filePath, FileMode.Open);
                var template = (LabelTemplate?)serializer.Deserialize(fs);
                
                if (template != null)
                {
                    LogMessage?.Invoke(this, $"Template loaded with {template.Elements.Count} elements");
                }
                
                return await Task.FromResult(template);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error loading template from {filePath}: {ex.Message}");
                return null;
            }
        }

        private async Task<DataTable?> LoadTemplateData(LabelTemplate template)
        {
            try
            {
                if (template.DataSource == null || string.IsNullOrWhiteSpace(template.DataSource.Query))
                    return null;

                LogMessage?.Invoke(this, $"Loading data for template: {template.TemplateName}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Executing query: {template.DataSource.Query}" + Environment.NewLine);
                
                // Execute MySQL query and return data table
                var mysqlService = new MySqlService
                {
                    ConnectionString = $"Server={template.DataSource.Host};Port={template.DataSource.Port};Database={template.DataSource.Database};Uid={template.DataSource.Username};Pwd={template.DataSource.Password};"
                };
                
                var dataTable = await Task.Run(() => mysqlService.ExecuteQuery(template.DataSource.Query));
                
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Query returned {dataTable?.Rows.Count ?? 0} records" + Environment.NewLine);
                LogMessage?.Invoke(this, $"Loaded {dataTable?.Rows.Count ?? 0} records for template: {template.TemplateName}");
                
                // Log column names and first few rows to verify data
                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Columns: {string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}" + Environment.NewLine);
                    
                    for (int i = 0; i < Math.Min(3, dataTable.Rows.Count); i++)
                    {
                        var row = dataTable.Rows[i];
                        var rowData = string.Join(", ", row.ItemArray.Select(v => v?.ToString() ?? "NULL"));
                        File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Row {i}: {rowData}" + Environment.NewLine);
                    }
                }
                
                return dataTable;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error loading data for template {template.TemplateName}: {ex.Message}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR loading data: {ex.Message}" + Environment.NewLine);
                return null;
            }
        }

        private async Task SendPrintJob(LabelTemplate template, string printerName, string templateName, DataTable? dataTable)
        {
            try
            {
                LogMessage?.Invoke(this, $"Sending print job for {templateName} to printer: {printerName}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sending print job for '{templateName}' to printer: {printerName}" + Environment.NewLine);
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Template details: Name='{template.TemplateName}', Elements={template.Elements.Count}" + Environment.NewLine);
                
                // Log first few elements to verify content
                for (int i = 0; i < Math.Min(5, template.Elements.Count); i++)
                {
                    var elem = template.Elements[i];
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]   Element {i}: Type={elem.Type}, Content='{elem.Content?.Substring(0, Math.Min(50, elem.Content?.Length ?? 0))}'" + Environment.NewLine);
                }
                
                // Determine how many labels to print
                int recordCount = dataTable?.Rows.Count ?? 1;
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Will print {recordCount} label(s)" + Environment.NewLine);
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataTable is null: {dataTable == null}" + Environment.NewLine);
                
                if (dataTable != null)
                {
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DataTable has {dataTable.Columns.Count} columns, {dataTable.Rows.Count} rows" + Environment.NewLine);
                }
                
                LogMessage?.Invoke(this, $"Printing {recordCount} label(s) for {templateName}");
                
                // WPF UI components must be created on the UI thread (STA thread)
                // Dispatch to the UI thread using the application's dispatcher
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var printService = new LabelPrintService();
                    
                    if (dataTable != null && dataTable.Rows.Count > 0)
                    {
                        // Check if dataTable has a "printer" column for per-record printer routing
                        if (dataTable.Columns.Contains("printer"))
                        {
                            File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 'printer' column found - grouping records by printer" + Environment.NewLine);
                            PrintRecordsGroupedByPrinter(printService, template, printerName, dataTable, templateName);
                        }
                        else
                        {
                            File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No 'printer' column - using default printer for all records" + Environment.NewLine);
                            // Print ALL records in ONE print job with multiple pages to default printer
                            printService.PrintAllRecords(template, printerName, dataTable);
                            
                            // Log the execution
                            AddLog(templateName, dataTable.Rows.Count, printerName);
                        }
                    }
                    else
                    {
                        // No data - print one label with static content
                        printService.Print(template, printerName, dataTable: null, recordIndex: 0, copies: 1);
                        
                        // Log the execution
                        AddLog(templateName, 1, printerName);
                    }
                });
                
                LogMessage?.Invoke(this, $"Print job sent successfully for {templateName}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Print job completed for {templateName}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error sending print job for {templateName}: {ex.Message}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR sending print job: {ex.Message}" + Environment.NewLine);
                
                // Log the failed execution
                AddLog(templateName, 0, printerName, $"Error: {ex.Message}");
            }
        }

        private void PrintRecordsGroupedByPrinter(LabelPrintService printService, LabelTemplate template, string defaultPrinter, DataTable dataTable, string templateName)
        {
            try
            {
                // Group records by printer name
                var printerGroups = new Dictionary<string, DataTable>();
                
                foreach (DataRow row in dataTable.Rows)
                {
                    // Get printer name from row (case-sensitive "printer" column)
                    var printerValue = row["printer"];
                    string targetPrinter = string.IsNullOrWhiteSpace(printerValue?.ToString()) 
                        ? defaultPrinter 
                        : printerValue.ToString()!.Trim();
                    
                    // Validate printer exists
                    var printerValidation = PrinterService.ValidatePrinter(targetPrinter);
                    if (!string.IsNullOrWhiteSpace(printerValidation.Warning))
                    {
                        // Printer invalid - skip this record and log
                        var recordId = row.ItemArray.Length > 0 ? row.ItemArray[0]?.ToString() : "unknown";
                        ErrorOccurred?.Invoke(this, $"Skipping record {recordId}: Invalid printer '{targetPrinter}' - {printerValidation.Warning}");
                        File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SKIPPED record {recordId}: Invalid printer '{targetPrinter}' - {printerValidation.Warning}" + Environment.NewLine);
                        continue;
                    }
                    
                    // Use validated printer name
                    targetPrinter = printerValidation.SelectedPrinter;
                    
                    // Create group if doesn't exist
                    if (!printerGroups.ContainsKey(targetPrinter))
                    {
                        printerGroups[targetPrinter] = dataTable.Clone(); // Same structure
                    }
                    
                    // Add row to this printer's group
                    printerGroups[targetPrinter].ImportRow(row);
                }
                
                // Print each group to its designated printer
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Grouped into {printerGroups.Count} printer group(s)" + Environment.NewLine);
                LogMessage?.Invoke(this, $"Printing to {printerGroups.Count} different printer(s)");
                
                foreach (var group in printerGroups)
                {
                    string groupPrinter = group.Key;
                    DataTable groupData = group.Value;
                    
                    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Printing {groupData.Rows.Count} record(s) to printer: {groupPrinter}" + Environment.NewLine);
                    LogMessage?.Invoke(this, $"Sending {groupData.Rows.Count} label(s) to {groupPrinter}");
                    
                    // Print all records in this group as one job
                    printService.PrintAllRecords(template, groupPrinter, groupData);
                    
                    // Log each printer group execution
                    AddLog(templateName, groupData.Rows.Count, groupPrinter);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error in grouped printing: {ex.Message}");
                File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in PrintRecordsGroupedByPrinter: {ex.Message}" + Environment.NewLine);
                
                // Log the failed execution
                AddLog(templateName, 0, defaultPrinter, $"Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Add a log entry for a print execution
        /// </summary>
        private void AddLog(string templateName, int recordCount, string printerName, string status = "Success")
        {
            lock (_lockObject)
            {
                var logEntry = new ScheduledPrintLogEntry
                {
                    ExecutionTime = DateTime.Now,
                    TemplateName = templateName,
                    RecordCount = recordCount,
                    PrinterName = printerName,
                    Status = status
                };

                _executionLogs.Insert(0, logEntry); // Add to beginning for newest-first order

                // Keep only the last 50 entries
                if (_executionLogs.Count > MaxLogEntries)
                {
                    _executionLogs.RemoveRange(MaxLogEntries, _executionLogs.Count - MaxLogEntries);
                }
            }
        }

        /// <summary>
        /// Get the last 50 execution logs
        /// </summary>
        public List<ScheduledPrintLogEntry> GetLogs()
        {
            lock (_lockObject)
            {
                return new List<ScheduledPrintLogEntry>(_executionLogs);
            }
        }
    }
}