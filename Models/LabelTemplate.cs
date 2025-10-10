using System.Collections.Generic;

namespace EvoDesigner.Models
{
    public class LabelTemplate
    {
        public string TemplateName { get; set; } = "";
        public double LabelWidth { get; set; }
        public double LabelHeight { get; set; }
        public double MarginLeft { get; set; }
        public double MarginRight { get; set; }
        public double MarginTop { get; set; }
        public double MarginBottom { get; set; }
        public List<LabelElement> Elements { get; set; } = new();
        public List<FieldMapping> FieldMappings { get; set; } = new();
        public DataSourceConfig? DataSource { get; set; }
    }

    public class DataSourceConfig
    {
        public string Host { get; set; } = "";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // Note: Consider encryption in production
        public string Query { get; set; } = "";
        public string DefaultPrinter { get; set; } = ""; // Flow mode: default printer name
        public string UpdateQuery { get; set; } = ""; // UPDATE statement to mark records as printed, e.g., "UPDATE labels SET printed = 1 WHERE id = {label_id}"
    }
}
