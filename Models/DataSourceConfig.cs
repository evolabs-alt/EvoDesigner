namespace TaggJack.Models;

public class DataSourceConfig
{
	public string Host { get; set; } = "";

	public string Port { get; set; } = "3306";

	public string Database { get; set; } = "";

	public string Username { get; set; } = "";

	public string Password { get; set; } = "";

	public string Query { get; set; } = "";

	public string DefaultPrinter { get; set; } = "";

	public string UpdateQuery { get; set; } = "";
}
