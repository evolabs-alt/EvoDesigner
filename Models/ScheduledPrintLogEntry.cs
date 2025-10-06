using System;

namespace EvoDesigner.Models
{
    /// <summary>
    /// Represents a log entry for a scheduled print execution
    /// </summary>
    public class ScheduledPrintLogEntry
    {
        /// <summary>
        /// The date and time when the print job was executed
        /// </summary>
        public DateTime ExecutionTime { get; set; }

        /// <summary>
        /// The name of the template that was printed
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>
        /// The number of records/labels printed
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// The name of the printer used
        /// </summary>
        public string PrinterName { get; set; } = string.Empty;

        /// <summary>
        /// Optional status or error message
        /// </summary>
        public string Status { get; set; } = "Success";

        /// <summary>
        /// Formatted display string for execution time
        /// </summary>
        public string FormattedTime => ExecutionTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
