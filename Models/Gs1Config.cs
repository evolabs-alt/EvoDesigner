namespace TaggJack.Models;

public class Gs1Config
{
	public string ApiUrl { get; set; } = "";

	public string BearerToken { get; set; } = "";

	public string DefaultBatch { get; set; } = "";

	public bool UseTodayForProduction { get; set; } = true;

	public string DefaultProductionDate { get; set; } = "";

	public string DefaultExpiryDate { get; set; } = "";

	public string FromGln { get; set; } = "";

	public string ToGln { get; set; } = "";

	public string DefaultOrderNumber { get; set; } = "";

	public string Level1TemplatePath { get; set; } = "";

	public string Level2TemplatePath { get; set; } = "";

	public string Level3TemplatePath { get; set; } = "";

	public string Level1Printer { get; set; } = "";

	public string Level2Printer { get; set; } = "";

	public string Level3Printer { get; set; } = "";
}
