using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaggJack.Models;

public class Gs1ActivationResponse
{
	[JsonPropertyName("result")]
	public string Result { get; set; } = "";

	[JsonPropertyName("sscc")]
	public string Sscc { get; set; } = "";

	[JsonPropertyName("shipper_count")]
	public int? ShipperCount { get; set; }

	[JsonPropertyName("primary_count")]
	public int? PrimaryCount { get; set; }

	[JsonPropertyName("shippers")]
	public List<Gs1ShipperResponse> Shippers { get; set; } = new List<Gs1ShipperResponse>();
}
