using System;

namespace TaggJack.Services.License;

public sealed class LicenseLeaseState
{
	public string? LeaseJwt { get; set; }
	public string? HardwareKey { get; set; }
	public DateTimeOffset? LastSuccessAt { get; set; }
	public string? Status { get; set; }
	public string? Message { get; set; }
	public string? LicenseExpiresAt { get; set; }
	public string? CustomerName { get; set; }

	public LicenseLeaseState Clone()
	{
		return new LicenseLeaseState
		{
			LeaseJwt = LeaseJwt,
			HardwareKey = HardwareKey,
			LastSuccessAt = LastSuccessAt,
			Status = Status,
			Message = Message,
			LicenseExpiresAt = LicenseExpiresAt,
			CustomerName = CustomerName
		};
	}
}
