namespace TaggJack.Models;

public class ErpImportConfig
{
	public string DatabaseType { get; set; } = "MySQL";

	public string Host { get; set; } = "";

	public string Port { get; set; } = "";

	public string Database { get; set; } = "";

	public string Username { get; set; } = "";

	public string Password { get; set; } = "";

	public string Query { get; set; } = "";

	public string ItemCodeField { get; set; } = "";

	public string GtinField { get; set; } = "";

	public string DescriptionField { get; set; } = "";

	public string Level1Field { get; set; } = "";

	public string Level2Field { get; set; } = "";

	public string Level3Field { get; set; } = "";
}
