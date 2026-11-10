using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaggJack.Models;

public class Gs1ShipperResponse
{
	[JsonPropertyName("serial")]
	public string Serial { get; set; } = "";

	[JsonPropertyName("primary_serials")]
	public List<string> PrimarySerials { get; set; } = new List<string>();
}
