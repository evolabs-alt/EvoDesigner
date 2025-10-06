using System.Collections.Generic;
using System.Text.Json;

namespace EvoDesigner.Models
{
    public class AppConfig
    {
        public string Mode { get; set; } = "designer"; // "designer" or "flow"
        public FlowModeConfig FlowMode { get; set; } = new();
    }

    public class FlowModeConfig
    {
        public bool SystemTrayEnabled { get; set; } = true;
        public bool ScheduledPrintingEnabled { get; set; } = true;
        public int PrintingIntervalSeconds { get; set; } = 60;
        public List<string> LoadedTemplatePaths { get; set; } = new();
        public bool StartMinimizedToTray { get; set; } = false;
        public bool ShowTrayNotifications { get; set; } = true;
    }

    public class LoadedTemplate
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public DateTime LastPrint { get; set; } = DateTime.MinValue;
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}
