namespace TaggJack.Tools;

public class LicenseRecord
{
	public string LicenseKey { get; set; } = "";

	public string LicenseType { get; set; } = "";

	public string CustomerName { get; set; } = "";

	public string CustomerEmail { get; set; } = "";

	public string Company { get; set; } = "";

	public string Notes { get; set; } = "";

	public string GeneratedDate { get; set; } = "";

	public int MaxActivations { get; set; }

	public string? ExpiryDate { get; set; }
}
