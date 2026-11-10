namespace TaggJack.Services.Inkjet;

public readonly record struct DataQueueResult(ushort Report, byte QueueStatus, byte PrinterStatus, ushort RemainingItems, uint RemainingMemoryBytes)
{
	public static DataQueueResult Missing { get; } = new DataQueueResult(ushort.MaxValue, byte.MaxValue, byte.MaxValue, 0, 0u);

	public bool IsMissing => Report == ushort.MaxValue;

	public bool Ok
	{
		get
		{
			ushort report = Report;
			if ((uint)(report - 1) <= 1u)
			{
				return true;
			}
			return false;
		}
	}
}
