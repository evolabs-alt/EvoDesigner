using System.Collections.Generic;

namespace TaggJack.Models;

public class FlowModeConfig
{
	public bool SystemTrayEnabled { get; set; } = true;

	public bool ScheduledPrintingEnabled { get; set; } = true;

	public int PrintingIntervalSeconds { get; set; } = 60;

	public List<string> LoadedTemplatePaths { get; set; } = new List<string>();

	public bool StartMinimizedToTray { get; set; }

	public bool ShowTrayNotifications { get; set; } = true;

	public bool UseWindowsServiceControl { get; set; }

	public string WindowsServiceName { get; set; } = "TaggJackPrintService";

	public bool TrayAutoStartAtLogon { get; set; } = true;
}
