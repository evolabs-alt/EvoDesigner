namespace TaggJack.Models;

public class AppConfig
{
	public string Mode { get; set; } = "designer";

	public bool Gs1Enabled { get; set; }

	public bool ErpImportEnabled { get; set; }

	public FlowModeConfig FlowMode { get; set; } = new FlowModeConfig();

	public Gs1Config Gs1 { get; set; } = new Gs1Config();

	public ErpImportConfig ErpImport { get; set; } = new ErpImportConfig();
}
