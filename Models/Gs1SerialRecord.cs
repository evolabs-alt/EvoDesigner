namespace TaggJack.Models;

public class Gs1SerialRecord
{
	public long Id { get; set; }

	public long JobId { get; set; }

	public int Level { get; set; }

	public string Serial { get; set; } = "";

	public string ParentSerial { get; set; } = "";

	public string Sscc { get; set; } = "";
}
