using System;

namespace TaggJack.Models;

public class ItemRecord
{
	public string ItemCode { get; set; } = "";

	public string Gtin { get; set; } = "";

	public string Description { get; set; } = "";

	public int Level1 { get; set; }

	public int Level2 { get; set; }

	public int Level3 { get; set; }

	public DateTime UpdatedAt { get; set; }
}
