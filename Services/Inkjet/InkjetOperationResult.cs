namespace TaggJack.Services.Inkjet;

public sealed class InkjetOperationResult
{
	public bool Success { get; init; }

	public string ProtocolLog { get; init; } = string.Empty;

	public string Message { get; init; } = string.Empty;
}
