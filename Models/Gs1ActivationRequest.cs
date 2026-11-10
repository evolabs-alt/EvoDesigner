using System.Text.Json.Serialization;

namespace TaggJack.Models;

public class Gs1PartyGln
{
	[JsonPropertyName("gln")]
	public string Gln { get; set; } = "";
}

public class Gs1ActivationRequest
{
	[JsonPropertyName("barcode")]
	public string Barcode { get; set; } = "";

	[JsonPropertyName("batch")]
	public string Batch { get; set; } = "";

	[JsonPropertyName("production")]
	public string Production { get; set; } = "";

	[JsonPropertyName("expiry")]
	public string Expiry { get; set; } = "";

	[JsonPropertyName("trade_item_qty")]
	public string TradeItemQty { get; set; } = "";

	[JsonPropertyName("shipper_qty")]
	public string ShipperQty { get; set; } = "";

	[JsonPropertyName("from")]
	public Gs1PartyGln From { get; set; } = new Gs1PartyGln();

	[JsonPropertyName("to")]
	public Gs1PartyGln To { get; set; } = new Gs1PartyGln();

	[JsonPropertyName("order_number")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? OrderNumber { get; set; }
}
