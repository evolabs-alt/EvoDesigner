namespace TaggJack.Models;

public class Gs1PackChunkPlan
{
	public int PalletOffset { get; set; }

	public int PalletsInChunk { get; set; }

	public int TradeItemQty { get; set; }

	public int ShipperQty { get; set; }
}
