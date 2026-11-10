using System;

namespace TaggJack.Models;

public class Gs1JobRecord
{
	public long Id { get; set; }

	public string ItemCode { get; set; } = "";

	public string Description { get; set; } = "";

	public string Gtin { get; set; } = "";

	public int Quantity { get; set; }

	public int Level1Count { get; set; }

	public int Level2Count { get; set; }

	public int Level3Count { get; set; }

	public string Batch { get; set; } = "";

	public string Production { get; set; } = "";

	public string Expiry { get; set; } = "";

	public string Level1TemplatePath { get; set; } = "";

	public string Level2TemplatePath { get; set; } = "";

	public string Level3TemplatePath { get; set; } = "";

	public string Level1Printer { get; set; } = "";

	public string Level2Printer { get; set; } = "";

	public string Level3Printer { get; set; } = "";

	public string RawResponseJson { get; set; } = "";

	public DateTime CreatedAt { get; set; }
}
