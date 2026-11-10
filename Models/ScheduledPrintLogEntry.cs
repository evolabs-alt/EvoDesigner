using System;

namespace TaggJack.Models;

public class ScheduledPrintLogEntry
{
	public DateTime ExecutionTime { get; set; }

	public string TemplateName { get; set; } = string.Empty;

	public int RecordCount { get; set; }

	public string PrinterName { get; set; } = string.Empty;

	public string Status { get; set; } = "Success";

	public string UpdateErrors { get; set; } = string.Empty;

	public string ProtocolLog { get; set; } = string.Empty;

	public bool HasProtocolLog => !string.IsNullOrWhiteSpace(ProtocolLog);

	public string DetailsLinkText
	{
		get
		{
			if (!HasProtocolLog)
			{
				return string.Empty;
			}
			return "View protocol log";
		}
	}

	public int NoRecordsCheckCount { get; set; } = 1;

	public string FormattedTime => ExecutionTime.ToString("yyyy-MM-dd HH:mm:ss");

	public string FormattedStatus
	{
		get
		{
			if (!(Status == "No records to print") || NoRecordsCheckCount <= 1)
			{
				return Status;
			}
			return $"No records to print ({NoRecordsCheckCount})";
		}
	}
}
