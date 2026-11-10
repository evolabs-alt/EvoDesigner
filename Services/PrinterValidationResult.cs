namespace TaggJack.Services;

public class PrinterValidationResult
{
	public bool IsValid { get; set; }

	public string SelectedPrinter { get; set; } = "";

	public string Warning { get; set; } = "";
}
